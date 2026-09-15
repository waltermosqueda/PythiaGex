"""
capas_respeto.py — ¿QUE FUENTE ACTUA MAS COMO SOPORTE/RESISTENCIA EN NQ? Cada capa contra su placebo.

Lee el centinela vivo del grafico de NQ (pythiagex-centinela-hoy-<inst>-TimeFrame-<marco>.jsonl), donde
Gamma Hoy 1.10 anota por vela cerrada las dominantes de cada capa activa (qqq_dom0, tqqq_dom0, ndx_dom0,
rithmic_dom0, spx_dom0, spy_dom0, es_dom0, ... y las de la primaria dom0/dom1), y juzga cada fuente con LA
MISMA regla que el laboratorio de siempre (rebote_niveles.py: llegada de lejos, toque de la banda, rebote R a
favor antes que R en contra en 20 min) y contra el MISMO placebo (el nivel corrido +-80 y +-140 pts de NQ).

Uso:
    python laboratorio/capas_respeto.py            # MNQ M2
    python laboratorio/capas_respeto.py MNQ M1

Como leerlo: la fuente "que mas actua" es la que rebota mas que su placebo con toques suficientes (25+).
Con pocos toques no hay veredicto: la muestra son niveles visitados, no minutos (memoria
la-muestra-son-niveles-no-minutos). El conteo que muestra el indicador en pantalla es solo el numerador.
"""
import io
import json
import os
import sys
from datetime import datetime

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rebote_niveles as rn   # la regla y el placebo, sin copiarlos

ATAS = os.path.join(os.environ.get("APPDATA", ""), "ATAS")
CAPAS = ("qqq", "tqqq", "ndx", "rithmic", "spx", "spy", "es")


def cargar(inst, marco):
    p = os.path.join(ATAS, "pythiagex-centinela-hoy-%s-TimeFrame-%s.jsonl" % (inst, marco))
    if not os.path.exists(p):
        sys.exit("no existe " + p)
    ult = {}
    for l in io.open(p, encoding="utf-8", errors="replace"):
        try:
            d = json.loads(l)
        except Exception:
            continue
        if d.get("niv") and "13:30" <= d["t"][11:16] < "20:00" and d.get("vol", 0) > 0:
            ult[d["t"]] = d
    vs = []
    for t in sorted(ult):
        d = ult[t]; n = d["niv"]; c = d["c"]
        v = dict(t=datetime.strptime(t[:19], "%Y-%m-%dT%H:%M:%S"), dia=t[:10], o=d["o"], h=d["h"], l=d["l"], c=c,
                 delta=float(d.get("delta") or 0), q=n.get("q_cuadrante"))
        for capa in ("",) + tuple(x + "_" for x in CAPAS):
            doms = [x for x in (n.get(capa + "dom0"), n.get(capa + "dom1")) if x]
            arr = [x for x in doms if x > c]; aba = [x for x in doms if x <= c]
            v[capa + "dom_arr"] = min(arr) if arr else None
            v[capa + "dom_aba"] = max(aba) if aba else None
        vs.append(v)
    return vs


def main():
    a = [x for x in sys.argv[1:] if not x.startswith("--")]
    inst = a[0] if a else "MNQ"; marco = a[1] if len(a) > 1 else "M2"
    es_nq = inst.startswith("MNQ") or inst.startswith("NQ")
    mpv = {"M1": 1, "M2": 2, "M5": 5}[marco]
    atras = {"M1": 5, "M2": 3, "M5": 2}[marco]
    reb = 20.0 if es_nq else 5.0; tol = 8.0 if es_nq else 2.0; horiz = 20
    lejos = 30.0 if es_nq else 7.0; p1 = 8.0 if es_nq else 2.0; p2 = 20.0 if es_nq else 5.0
    placebos = (-140, -80, 80, 140) if es_nq else (-35, -20, 20, 35)
    vs = cargar(inst, marco)
    dias = sorted(set(v["dia"] for v in vs))
    print("%s %s: %d velas de rueda en %d dias (%s .. %s); banda %.0f, lejos %.0f, R %.0f, horizonte %d min, placebo nivel corrido %s" % (
        inst, marco, len(vs), len(dias), dias[0] if dias else "-", dias[-1] if dias else "-", tol, lejos, reb, horiz, placebos))
    filas = []
    for capa in ("",) + tuple(x + "_" for x in CAPAS):
        nombre = "PRIMARIA" if capa == "" else capa[:-1].upper()
        con_dato = sum(1 for v in vs if v.get(capa + "dom_arr") or v.get(capa + "dom_aba"))
        if con_dato == 0:
            continue
        rn.NIVELES = (capa + "dom_arr", capa + "dom_aba")
        real = [t for t in rn.toques(vs, mpv, tol, lejos, p1, p2, reb, horiz, atras) if t["cat"] != "ruptura"]
        plc = []
        for corr in placebos:
            plc += [t for t in rn.toques(vs, mpv, tol, lejos, p1, p2, reb, horiz, atras, corr) if t["cat"] != "ruptura"]
        pr = 100.0 * sum(1 for t in real if t["rebote"]) / len(real) if real else float("nan")
        pp = 100.0 * sum(1 for t in plc if t["rebote"]) / len(plc) if plc else float("nan")
        filas.append((nombre, con_dato, len(real), pr, len(plc), pp, pr - pp if real and plc else float("nan")))
    filas.sort(key=lambda f: (-(f[6] if f[6] == f[6] else -999), -f[2]))
    print()
    print("  %-9s %7s %7s %9s %9s %9s %9s" % ("fuente", "velas", "toques", "rebota %", "placebo n", "placebo %", "ventaja"))
    for f in filas:
        vered = "candidata" if f[2] >= 25 and f[3] == f[3] and f[3] >= 60 and f[6] >= 10 else ("muestra corta" if f[2] < 25 else "no le gana al nivel corrido")
        print("  %-9s %7d %7d %8.1f%% %9d %8.1f%% %+8.1f pp   %s" % (f[0], f[1], f[2], f[3], f[4], f[5], f[6], vered))
    print()
    print("COMO LEERLO: 'candidata' = rebota 60 %+ con 10 pp+ sobre el placebo y 25+ toques. Lo demas no se afirma.")
    print("Las capas SPX/SPY/ES estan llevadas a NQ por beta medida en la rueda: si la beta era SUPUESTA en esas velas, el nivel es una hipotesis.")


if __name__ == "__main__":
    main()
