# -*- coding: utf-8 -*-
"""RONDA 26, paso 4: COMPROBACION DESDE EL CRUDO, con codigo aparte (no usa r26_comun para calcular: solo para leer lo que dieron los scripts).
Por cada dia pedido, en ES y en MES: parseo de la cinta como test_ola.referencia (csv + datetime.fromisoformat + float), la Maquina de
test_ola, y lazos simples para:
  - el estado en varios cierres de minuto (direccion, extremo, origen, delta de la pierna, color, retroceso r/Xs, caja, desenlace SIGUE),
    y el delta por tamano de orden desde el origen; se compara campo por campo con la tabla de minutos (armar_minutos);
  - una pierna confirmada de la marea por tamano (ES y MES), contra armar_piernas.
Uso: python -I r26_04_comprobacion.py <dia> [<dia> ...]"""
import csv
import json
import os
import sys
from datetime import datetime, timezone
from zoneinfo import ZoneInfo

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r26_comun as C     # solo rutas, archivo() y las tablas que dieron los scripts

NY = ZoneInfo("America/New_York")
UTC = timezone.utc
MINUTOS = [0, 15, 60, 120, 180, 240, 300, 360, 389]
BORDES = {"ES": [(1, 1, "1"), (2, 4, "2-4"), (5, 9, "5-9"), (10, 19, "10-19"), (20, 49, "20-49"), (50, 10 ** 9, "50+")],
          "MES": [(1, 1, "1"), (2, 4, "2-4"), (5, 9, "5-9"), (10, 19, "10-19"), (20, 49, "20-49"), (50, 99, "50-99"), (100, 199, "100-199"),
                  (200, 10 ** 9, "200+")]}


def tramo(inst, v):
    for a, b, et in BORDES[inst]:
        if a <= v <= b:
            return et
    raise ValueError(v)


def ms_de_ny(dia, hh, mm, dd=0):
    from datetime import timedelta
    d = datetime.strptime(dia, "%Y-%m-%d") + timedelta(days=dd)
    return int(d.replace(hour=hh, minute=mm, tzinfo=NY).timestamp() * 1000)


def filas(inst, dia):
    w0, w1 = ms_de_ny(dia, 18, 0, -1), ms_de_ny(dia, 17, 0)
    out = []
    with open(C.archivo(inst, dia), newline="") as fh:
        r = csv.reader(fh)
        next(r)
        for x in r:
            t = int(round(datetime.fromisoformat(x[0]).replace(tzinfo=UTC).timestamp() * 1000))
            if w0 <= t < w1:
                out.append((t, float(x[1]), float(x[2]), float(x[3]), int(float(x[4]))))
    return out


def estados(inst, dia, Xs, F):
    """Corre la Maquina de referencia y guarda el estado en los cierres pedidos (antes de la primera orden de t >= cierre)."""
    r0 = ms_de_ny(dia, 9, 30)
    cortes = [(m, r0 + 60000 * (m + 1)) for m in MINUTOS]
    q = C.T.Maquina(Xs)
    cum = 0.0
    cums = []
    k = 0
    est = {}
    for i, (t, a, b, v, lado) in enumerate(F):
        while k < len(cortes) and t >= cortes[k][1]:
            m = cortes[k][0]
            est[m] = {"i": i - 1, "dir": q.dir, "ext": q.ext, "npiv": len(q.pivotes), "cum": cum, "ult": F[i - 1][2]}
            k += 1
        cum += v * lado
        cums.append(cum)
        q.paso(t, min(a, b), max(a, b), cum)
    while k < len(cortes):
        est[cortes[k][0]] = {"i": len(F) - 1, "dir": q.dir, "ext": q.ext, "npiv": len(q.pivotes), "cum": cum, "ult": F[-1][2]}
        k += 1
    for m, e in est.items():
        o = q.pivotes[e["npiv"] - 1]
        e["o_t"], e["o_p"], e["o_cum"] = o[0], o[1], o[4]
        # indice de la orden del origen: la primera con esa hora y ese acumulado
        io = next(j for j in range(len(F)) if F[j][0] == o[0] and cums[j] == o[4])
        e["delta"] = e["cum"] - o[4]
        e["con"] = (e["delta"] > 0 and e["dir"] == 1) or (e["delta"] < 0 and e["dir"] == -1)
        e["r"] = abs(e["ext"] - e["ult"]) / Xs
        e["caja"] = 0 if e["r"] < 0.25 else 1 if e["r"] < 0.5 else 2 if e["r"] < 0.75 else 3
        fin = q.pivotes[e["npiv"]][1] if e["npiv"] < len(q.pivotes) else q.ext
        e["sigue"] = e["dir"] * (fin - e["ext"]) >= Xs / 2
        e["resuelto"] = e["npiv"] < len(q.pivotes) or e["sigue"]
        por = {}
        for j in range(io + 1, e["i"] + 1):
            et = tramo(inst, F[j][3])
            por[et] = por.get(et, 0.0) + F[j][3] * F[j][4]
        e["por"] = por
    return est, q, cums


def main():
    pares = json.load(open(os.path.join(C.DATOS, "pares.json")))
    malos = 0
    comparados = 0
    for dia in sys.argv[1:]:
        X = pares[dia]["X"]
        F = {inst: filas(inst, dia) for inst in ("ES", "MES")}
        for esc, fac in (("X", 1), ("4X", 4)):
            Xs = fac * X
            tabla = C.armar_minutos(dia, esc, X).set_index("m")
            E = {}
            for inst, pref in (("ES", "e_"), ("MES", "m_")):
                est, q, cums = estados(inst, dia, Xs, F[inst])
                E[inst] = est
                for m, e in est.items():
                    fila = tabla.loc[m]
                    pares_c = [("dir", e["dir"], fila[pref + "dir"]), ("ext", e["ext"], fila[pref + "ext"]), ("origen_t", e["o_t"], fila[pref + "o_t"]),
                               ("delta", e["delta"], fila[pref + "delta"]), ("con", e["con"], bool(fila[pref + "con"])), ("r", round(e["r"], 9), round(fila[pref + "r"], 9)),
                               ("sigue", e["sigue"], bool(fila[pref + "sigue"])), ("resuelto", e["resuelto"], bool(fila[pref + "resuelto"]))]
                    for et in [x[2] for x in BORDES[inst]]:
                        pares_c.append(("d_" + et, e["por"].get(et, 0.0), fila[pref + "d_" + et]))
                    if inst == "ES":
                        pares_c.append(("caja", e["caja"], int(fila["caja"])))
                    dif = [(n, a, b) for n, a, b in pares_c if a != b]
                    comparados += len(pares_c)
                    malos += len(dif)
                    print("%s %s %-3s min %3d (%s NY): dir %+d ext %.2f origen %s %.2f delta %+.0f %s r %.3f -> %s | %s" %
                          (dia, esc, inst, m, datetime.fromtimestamp((ms_de_ny(dia, 9, 30) + 60000 * (m + 1)) / 1000, NY).strftime("%H:%M"), e["dir"], e["ext"],
                           datetime.fromtimestamp(e["o_t"] / 1000, NY).strftime("%H:%M:%S"), e["o_p"], e["delta"], "CON" if e["con"] else "SIN", e["r"],
                           ("SIGUE" if e["sigue"] else "GIRA") if e["resuelto"] else "censurado", "IGUAL" if not dif else "DISTINTO %s" % dif))
            for m in MINUTOS:
                cl = ("C" if E["ES"][m]["con"] else "S") + ("C" if E["MES"][m]["con"] else "S")
                comparados += 1
                if cl != tabla.loc[m, "clase"]:
                    malos += 1
                    print("   clase min %d: crudo %s, tabla %s" % (m, cl, tabla.loc[m, "clase"]))
        # una pierna de la marea: la de mayor recorrido que termina en la rueda, por tamano, ES y MES
        P = C.armar_piernas(dia, "4X")
        P = P[P["emparejada"]]
        if len(P):
            g = P.loc[P["recorrido"].idxmax()]
            for inst, pref in (("ES", "e_"), ("MES", "m_")):
                Fi = F[inst]
                if inst == "ES":
                    t0, t1 = int(g["t0"]), int(g["t1"])
                else:
                    t0, t1 = int(round(g["t0"] + g["m_dt0_s"] * 1000)), int(round(g["t1"] + g["m_dt1_s"] * 1000))
                q = C.T.Maquina(4 * X)
                cum = 0.0
                cums = []
                for (t, a, b, v, lado) in Fi:
                    cum += v * lado
                    cums.append(cum)
                    q.paso(t, min(a, b), max(a, b), cum)
                pv = {p[0]: p for p in q.pivotes}
                i0 = next(j for j in range(len(Fi)) if Fi[j][0] == t0 and cums[j] == pv[t0][4])
                i1 = next(j for j in range(len(Fi)) if Fi[j][0] == t1 and cums[j] == pv[t1][4])
                por = {}
                for j in range(i0 + 1, i1 + 1):
                    et = tramo(inst, Fi[j][3])
                    por[et] = por.get(et, 0.0) + Fi[j][3] * Fi[j][4]
                tot = sum(por.values())
                ok = abs(tot - g[pref + "delta"]) < 1e-9 and all(abs(por.get(et, 0.0) - g[pref + "d_" + et]) < 1e-9 for et in [x[2] for x in BORDES[inst]])
                comparados += 1
                malos += not ok
                print("%s marea, pierna %s %s -> %s (%.2f -> %.2f, %.2f pts), %s: delta %+.0f = %s -> %s" %
                      (dia, "sube" if g["dir"] > 0 else "baja", datetime.fromtimestamp(t0 / 1000, NY).strftime("%H:%M:%S"),
                       datetime.fromtimestamp(t1 / 1000, NY).strftime("%H:%M:%S"), g["p0"], g["p1"], g["recorrido"], inst, tot,
                       ", ".join("%s %+.0f" % (k, v) for k, v in sorted(por.items(), key=lambda kv: [x[2] for x in BORDES[inst]].index(kv[0]))),
                       "IGUAL a armar_piernas" if ok else "DISTINTO de armar_piernas"))
    print("RESULTADO: %d comparaciones, %d diferencias" % (comparados, malos))


if __name__ == "__main__":
    main()
